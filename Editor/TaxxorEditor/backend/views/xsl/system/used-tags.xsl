<?xml version="1.0" encoding="UTF-8"?>

<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xhtml="http://www.w3.org/1999/xhtml" exclude-result-prefixes="xs xhtml" version="2.0">



    <xsl:output method="xml" indent="yes"/>


    <xsl:variable name="newline">
        <xsl:text>
</xsl:text>
    </xsl:variable>
    
    
    
    <!-- Store all the nodes in a key node -->
    <xsl:key name="elements" match="*" use="name()"/>

    <xsl:template match="/">

        <!-- Create a unique list of elements used in the document -->
        <xsl:variable name="used-elements">
            <used-elements>

                <xsl:for-each select="//*[generate-id(.) = generate-id(key('elements', name())[1])]">
                    <xsl:sort select="name()"/>
                    <xsl:for-each select="key('elements', name())">
                        <xsl:if test="position() = 1">
                            <xsl:variable name="node-name" select="local-name()"/>
                            <xsl:element name="{$node-name}">

                                <!-- The number of occurances in the document -->
                                <occurances>
                                    <xsl:value-of select="count(//*[name() = name(current())])"/>
                                </occurances>
                            </xsl:element>

                        </xsl:if>
                    </xsl:for-each>
                </xsl:for-each>

            </used-elements>
        </xsl:variable>
        
        <!--<xsl:copy-of select="$used-elements"/>-->
        
        <xsl:variable name="use-xhtml">
            <xsl:choose>
                <xsl:when test="count(html) = 0">true</xsl:when>
                <xsl:otherwise>false</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        

        <xsl:apply-templates select="html | foo">
            <xsl:with-param name="used-elements" select="$used-elements"/>
            <xsl:with-param name="use-xhtml" select="$use-xhtml"/>
        </xsl:apply-templates>
        
    </xsl:template>
    
    <xsl:template match="html | foo">
        <xsl:param name="used-elements"/>
        <xsl:param name="use-xhtml"/>
        
        <xsl:variable name="doc" select="/"/>
        
        <used-elements>
            <xsl:attribute name="use-xml">
                <xsl:value-of select="$use-xhtml"/>
            </xsl:attribute>
            
            <xsl:for-each select="$used-elements/used-elements/*">
                <xsl:variable name="node-name" select="local-name(.)"/>
                
                <xsl:element name="{$node-name}">
                    
                    <xsl:variable name="doc-nodelist">
                        <xsl:choose>
                            <xsl:when test="$use-xhtml = 'true'">
                                <xsl:value-of select="$doc//xhtml:*[local-name() = $node-name]"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:value-of select="$doc//*[local-name() = $node-name]"/>
                            </xsl:otherwise>
                        </xsl:choose>
                        
                    </xsl:variable>
                    
                    <!-- add the attributes that were used in the document -->
                    <xsl:for-each select="$doc//*[local-name() = $node-name]">
                        
                        <xsl:for-each select="current()/@*">
                            <xsl:variable name="attribute-name" select="local-name(.)"/>
                            <xsl:attribute name="{$attribute-name}"></xsl:attribute>
                        </xsl:for-each>      

                    </xsl:for-each>
                    
                    
                    
                    <!--<xsl:apply-templates select="$doc//*[local-name() = $node-name]" mode="find-attributes"/>-->
                    <!--(<xsl:value-of select="count($doc//*[local-name() = $node-name])"/>)-->
                    
                    <xsl:copy-of select="occurances"/>
                </xsl:element>
                
            </xsl:for-each>
            
        </used-elements>
        
        
        
    </xsl:template>




</xsl:stylesheet>
