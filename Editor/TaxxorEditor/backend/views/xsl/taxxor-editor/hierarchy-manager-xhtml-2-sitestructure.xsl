<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    
    <xsl:param name="doc-hierarchy"/>
    <xsl:param name="doc-allitems"/>
    
    <xsl:output method="xml" encoding="UTF-8" indent="yes" />
    
    <xsl:template match="@*|*|processing-instruction()|comment()">
        <xsl:copy>
            <xsl:apply-templates select="*|@*|text()|processing-instruction()|comment()"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="/">
        <items>
            <structured>
                <xsl:apply-templates/>
            </structured>
        </items>
    </xsl:template>
    
    <xsl:template match="li">
        <xsl:variable name="hierarchy-id" select="@hierarchy-id"/>
        <xsl:variable name="data-ref" select="@data-ref"/>
        <xsl:variable name="level" select="count(ancestor::li)"/>
        
        <xsl:variable name="doc-hierarchy-item" select="$doc-hierarchy//item[@data-ref=$data-ref]"/>
        
        <item id="{@hierarchy-id}" level="{$level}" data-ref="{@data-ref}">
            <xsl:if test="@data-tocstart">
                <xsl:attribute name="data-tocstart">true</xsl:attribute>
            </xsl:if>
            <xsl:if test="@data-tocend">
                <xsl:attribute name="data-tocend">true</xsl:attribute>
            </xsl:if>
            <xsl:if test="@data-tocstyle">
                <xsl:attribute name="data-tocstyle">
                    <xsl:value-of select="@data-tocstyle"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="@data-tochide">
                <xsl:attribute name="data-tochide">true</xsl:attribute>
            </xsl:if>
            
            <!-- use the data-ref link to search for the nodes from the original site structure -->
            <xsl:choose>
                <xsl:when test="count($doc-hierarchy-item) &gt; 0">
                    <web_page>
                        <path>
                            <xsl:value-of select="div/span[@class='data']/span[@class='path']"/>
                        </path>
                        <linkname>
                            <xsl:value-of select="div/span[@class='linkname']"/>
                        </linkname>
                    </web_page>
                    <xsl:copy-of select="$doc-hierarchy-item/access_control"/>                    
                </xsl:when>
                <xsl:otherwise>
                    <!-- This is a new item in the hierarchy which was dragged -->
                    <xsl:variable name="doc-new-hierarchy-item" select="$doc-allitems//item[@data-ref=$data-ref]"/>
                    <web_page>
                        <path>/</path>
                        <linkname>
                            <xsl:value-of select="$doc-new-hierarchy-item/h1/text()"/>
                        </linkname>
                    </web_page>
                </xsl:otherwise>
            </xsl:choose>

            <xsl:apply-templates select="ul[li]"/>
        </item>
        
    </xsl:template>
    
    <xsl:template match="ul">
        <sub_items>
            <xsl:apply-templates select="li"/>
        </sub_items>
    </xsl:template>
    
    
</xsl:stylesheet>