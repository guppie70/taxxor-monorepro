<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:output method="xml" omit-xml-declaration="no" indent="no" encoding="UTF-8"/>

    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="commits">
        <commits>
            <xsl:for-each select="commit-meta">
                <xsl:sort select="commit/date/@epoch" order="descending" data-type="number"/>
                <xsl:apply-templates select="commit">
                    <xsl:with-param name="commit-hash" select="@hash"/>
                    <xsl:with-param name="repro-id" select="@repro"/>
                    <xsl:with-param name="latest">
                        <xsl:choose>
                            <xsl:when test="@latest">false</xsl:when>
                            <xsl:otherwise>true</xsl:otherwise>
                        </xsl:choose>
                    </xsl:with-param>
                </xsl:apply-templates>
            </xsl:for-each>
        </commits>
    </xsl:template>
	
	<xsl:template match="commit">
	    <xsl:param name="commit-hash"/>
	    <xsl:param name="repro-id"/>
	    <xsl:param name="latest"/>
	   
	   <commit repro="{$repro-id}" hash="{$commit-hash}" latest="{$latest}">
	       <xsl:apply-templates/>
	   </commit>
	
	</xsl:template>

</xsl:stylesheet>
